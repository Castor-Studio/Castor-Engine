function(_castor_validate_obs_sdk sdk_root)
    set(required_files
        "${sdk_root}/manifest.json"
        "${sdk_root}/include/obs.h"
        "${sdk_root}/include/obsconfig.h"
        "${sdk_root}/lib/obs.lib"
        "${sdk_root}/lib/w32-pthreads.lib"
        "${sdk_root}/cmake/libobsConfig.cmake"
        "${sdk_root}/runtime/bin/64bit/obs.dll"
        "${sdk_root}/runtime/bin/64bit/w32-pthreads.dll"
    )

    foreach(required_file IN LISTS required_files)
        if(NOT EXISTS "${required_file}")
            message(FATAL_ERROR
                "Castor OBS SDK file was not found: ${required_file}"
            )
        endif()
    endforeach()

    file(READ "${sdk_root}/manifest.json" manifest)
    string(JSON manifest_sdk_version GET "${manifest}" sdkVersion)
    string(JSON manifest_runtime_identifier GET "${manifest}" runtimeIdentifier)

    if(NOT manifest_sdk_version STREQUAL CASTOR_OBS_SDK_VERSION)
        message(FATAL_ERROR
            "Castor OBS SDK version mismatch. "
            "Expected ${CASTOR_OBS_SDK_VERSION}, "
            "received ${manifest_sdk_version}."
        )
    endif()

    if(NOT manifest_runtime_identifier STREQUAL CASTOR_OBS_SDK_PLATFORM)
        message(FATAL_ERROR
            "Castor OBS SDK platform mismatch. "
            "Expected ${CASTOR_OBS_SDK_PLATFORM}, "
            "received ${manifest_runtime_identifier}."
        )
    endif()
endfunction()

function(castor_bootstrap_obs_sdk)
    set(
        CASTOR_OBS_SDK_ARCHIVE_PATH
        ""
        CACHE FILEPATH
        "Path to the Castor OBS SDK archive"
    )

    set(
        CASTOR_OBS_SDK_ROOT
        ""
        CACHE PATH
        "Path to an extracted Castor OBS SDK"
    )

    if(CASTOR_OBS_SDK_ROOT)
        get_filename_component(
            sdk_root
            "${CASTOR_OBS_SDK_ROOT}"
            ABSOLUTE
            BASE_DIR "${CMAKE_SOURCE_DIR}"
        )
    else()
        set(archive_path "${CASTOR_OBS_SDK_ARCHIVE_PATH}")

        if(NOT archive_path AND DEFINED ENV{CASTOR_OBS_SDK_ARCHIVE_PATH})
            set(archive_path "$ENV{CASTOR_OBS_SDK_ARCHIVE_PATH}")
        endif()

        if(NOT archive_path)
            message(FATAL_ERROR
                "The Castor OBS SDK is not available. "
                "Set CASTOR_OBS_SDK_ARCHIVE_PATH to "
                "${CASTOR_OBS_SDK_ARCHIVE_NAME}."
            )
        endif()

        get_filename_component(
            archive_path
            "${archive_path}"
            ABSOLUTE
            BASE_DIR "${CMAKE_SOURCE_DIR}"
        )

        if(NOT EXISTS "${archive_path}")
            message(FATAL_ERROR
                "Castor OBS SDK archive was not found: ${archive_path}"
            )
        endif()

        file(SHA256 "${archive_path}" archive_sha256)

        if(NOT archive_sha256 STREQUAL CASTOR_OBS_SDK_SHA256)
            message(FATAL_ERROR
                "Castor OBS SDK checksum mismatch. "
                "Expected ${CASTOR_OBS_SDK_SHA256}, "
                "received ${archive_sha256}."
            )
        endif()

        set(
            sdk_root
            "${CMAKE_BINARY_DIR}/_deps/castor-obs-sdk-${CASTOR_OBS_SDK_VERSION}"
        )
        set(checksum_stamp "${sdk_root}/.castor-sdk-sha256")
        set(extract_sdk TRUE)

        if(EXISTS "${checksum_stamp}")
            file(READ "${checksum_stamp}" extracted_sha256)
            string(STRIP "${extracted_sha256}" extracted_sha256)

            if(extracted_sha256 STREQUAL CASTOR_OBS_SDK_SHA256)
                set(extract_sdk FALSE)
            endif()
        endif()

        if(extract_sdk)
            set(staging_root "${sdk_root}.tmp")
            file(REMOVE_RECURSE "${staging_root}")
            file(REMOVE_RECURSE "${sdk_root}")
            file(MAKE_DIRECTORY "${staging_root}")

            message(STATUS
                "Extracting Castor OBS SDK ${CASTOR_OBS_SDK_VERSION}"
            )

            file(
                ARCHIVE_EXTRACT
                INPUT "${archive_path}"
                DESTINATION "${staging_root}"
            )

            _castor_validate_obs_sdk("${staging_root}")

            file(
                RENAME
                "${staging_root}"
                "${sdk_root}"
                RESULT rename_result
            )

            if(rename_result)
                message(FATAL_ERROR
                    "Failed to publish the extracted Castor OBS SDK: "
                    "${rename_result}"
                )
            endif()

            file(
                WRITE
                "${checksum_stamp}"
                "${CASTOR_OBS_SDK_SHA256}\n"
            )
        endif()
    endif()

    _castor_validate_obs_sdk("${sdk_root}")

    find_package(
        libobs
        CONFIG
        REQUIRED
        NO_DEFAULT_PATH
        PATHS "${sdk_root}/cmake"
    )

    if(NOT TARGET OBS::libobs)
        message(FATAL_ERROR
            "The Castor OBS SDK did not define OBS::libobs."
        )
    endif()

    set(
        CASTOR_OBS_SDK_ROOT
        "${sdk_root}"
        CACHE PATH
        "Path to an extracted Castor OBS SDK"
        FORCE
    )

    set(CASTOR_OBS_SDK_ROOT "${sdk_root}" PARENT_SCOPE)

    message(STATUS
        "Using Castor OBS SDK ${CASTOR_OBS_SDK_VERSION}: ${sdk_root}"
    )
endfunction()
