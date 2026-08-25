#include "recording_subsystem.h"

#include "recording_configuration.h"

#include <chrono>
#include <condition_variable>
#include <filesystem>
#include <mutex>
#include <obs.h>
#include <utility>

namespace castor::engine::detail
{
namespace
{
constexpr const char* recording_output_id = "ffmpeg_muxer";
constexpr std::chrono::seconds stop_wait_timeout{10};

recording_lifecycle_result failure(castor_engine_result_t code, std::string message)
{
    return {code, std::move(message)};
}

struct stop_wait_context
{
    std::mutex mutex;
    std::condition_variable condition;
    bool signaled = false;
};

void handle_stop_signal(void* data, calldata_t*)
{
    auto* context = static_cast<stop_wait_context*>(data);

    {
        std::scoped_lock lock(context->mutex);
        context->signaled = true;
    }

    context->condition.notify_all();
}
} // namespace

recording_lifecycle_result recording_subsystem::start(const castor_engine_recording_config_t* config,
                                                      bool runtime_ready, bool video_ready, bool scene_active,
                                                      void* video_encoder_handle, void* audio_encoder_handle)
{
    const recording_configuration_result validation_result = validate_recording_config(config);

    if (validation_result.code != CASTOR_ENGINE_OK)
    {
        return {validation_result.code, validation_result.message};
    }

    if (!runtime_ready)
    {
        return failure(CASTOR_ENGINE_NOT_INITIALIZED, "The engine must be initialized before recording can start.");
    }

    if (!video_ready)
    {
        return failure(CASTOR_ENGINE_VIDEO_NOT_CONFIGURED,
                       "The video subsystem must be configured before recording can start.");
    }

    if (!scene_active)
    {
        return failure(CASTOR_ENGINE_RECORDING_NO_ACTIVE_SCENE,
                       "A scene must be active before recording can start.");
    }

    if (active_)
    {
        return failure(CASTOR_ENGINE_RECORDING_ALREADY_ACTIVE,
                       "A recording is already active. Stop it before starting a new one.");
    }

    // A deterministic, engine-side check ahead of ever touching OBS -
    // ffmpeg_muxer itself only reports a path failure once obs_output_start
    // is already underway, which would otherwise fold this into a generic
    // start failure with no actionable distinction.
    const auto destination_path = std::filesystem::u8path(config->destination_path);
    const auto parent_directory = destination_path.parent_path();
    std::error_code filesystem_error;

    if (!parent_directory.empty() && !std::filesystem::is_directory(parent_directory, filesystem_error))
    {
        return failure(CASTOR_ENGINE_RECORDING_INVALID_DESTINATION,
                       std::string("The recording destination directory does not exist: ") + config->destination_path +
                           ".");
    }

    if (obs_get_output_flags(recording_output_id) == 0)
    {
        return failure(CASTOR_ENGINE_RECORDING_OUTPUT_UNAVAILABLE,
                       "The packaged OBS FFmpeg muxer output is not available.");
    }

    obs_data_t* settings = obs_data_create();
    obs_data_set_string(settings, "path", config->destination_path);

    obs_output_t* output = obs_output_create(recording_output_id, "castor-recording-output", settings, nullptr);
    obs_data_release(settings);

    if (output == nullptr)
    {
        return failure(CASTOR_ENGINE_RECORDING_OUTPUT_CREATION_FAILED,
                       "OBS failed to create the MKV recording output.");
    }

    obs_output_set_video_encoder(output, static_cast<obs_encoder_t*>(video_encoder_handle));
    obs_output_set_audio_encoder(output, static_cast<obs_encoder_t*>(audio_encoder_handle), 0);

    if (!obs_output_start(output))
    {
        const char* obs_error = obs_output_get_last_error(output);
        std::string message = "OBS failed to start the MKV recording output.";

        if (obs_error != nullptr && obs_error[0] != '\0')
        {
            message += " OBS reported: ";
            message += obs_error;
        }

        obs_output_release(output);
        return failure(CASTOR_ENGINE_RECORDING_START_FAILED, message);
    }

    output_ = output;
    active_ = true;
    return {CASTOR_ENGINE_OK, {}};
}

recording_lifecycle_result recording_subsystem::stop()
{
    if (!active_)
    {
        return failure(CASTOR_ENGINE_RECORDING_NOT_ACTIVE, "No recording is active.");
    }

    auto* output = static_cast<obs_output_t*>(output_);

    // obs_output_stop only requests a stop and returns immediately; OBS
    // finalizes the MKV container asynchronously. Block on the output's own
    // "stop" signal so the container is guaranteed finalized before the
    // output is released, with a bounded timeout as a defensive fallback
    // rather than a spec requirement.
    signal_handler_t* signal_handler = obs_output_get_signal_handler(output);
    stop_wait_context context;
    signal_handler_connect(signal_handler, "stop", handle_stop_signal, &context);

    obs_output_stop(output);

    {
        std::unique_lock lock(context.mutex);
        context.condition.wait_for(lock, stop_wait_timeout, [&context] { return context.signaled; });
    }

    signal_handler_disconnect(signal_handler, "stop", handle_stop_signal, &context);

    obs_output_release(output);
    output_ = nullptr;
    active_ = false;

    return {CASTOR_ENGINE_OK, {}};
}

bool recording_subsystem::is_active() noexcept
{
    return active_;
}

void recording_subsystem::reset() noexcept
{
    if (active_)
    {
        stop();
        return;
    }

    if (output_ != nullptr)
    {
        obs_output_release(static_cast<obs_output_t*>(output_));
        output_ = nullptr;
    }
}
} // namespace castor::engine::detail
