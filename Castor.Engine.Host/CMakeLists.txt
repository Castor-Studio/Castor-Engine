add_library(Castor.Engine.Host SHARED
    src/castor_engine.cpp
    src/video_configuration.cpp
    src/audio_configuration.cpp
    src/audio_subsystem.cpp
    src/main_scene.cpp
    src/obs_scene_backend.cpp
    src/video_encoder_configuration.cpp
    src/video_encoder_enumeration.cpp
    src/video_encoder_subsystem.cpp
    src/audio_encoder_configuration.cpp
)

target_include_directories(
    Castor.Engine.Host
    PUBLIC
        ${CMAKE_CURRENT_SOURCE_DIR}/include
)

target_compile_definitions(
    Castor.Engine.Host
    PRIVATE
        CASTOR_ENGINE_HOST_EXPORTS
)

target_compile_features(
    Castor.Engine.Host
    PRIVATE
        cxx_std_20
)

target_link_libraries(
    Castor.Engine.Host
    PRIVATE
        OBS::libobs
)

set_target_properties(
    Castor.Engine.Host
    PROPERTIES
        OUTPUT_NAME "Castor.Engine.Host"
)

install(
    TARGETS Castor.Engine.Host
    RUNTIME DESTINATION bin
    LIBRARY DESTINATION bin
    ARCHIVE DESTINATION lib
)

install(
    DIRECTORY "${CASTOR_OBS_SDK_ROOT}/runtime/bin/64bit/"
    DESTINATION bin
)

install(
    DIRECTORY "${CASTOR_OBS_SDK_ROOT}/runtime/obs-plugins/"
    DESTINATION obs-plugins
)

install(
    DIRECTORY "${CASTOR_OBS_SDK_ROOT}/runtime/data/"
    DESTINATION data
)

add_executable(Castor.Engine.Host.Tests
    tests/main_scene_tests.cpp
    src/main_scene.cpp
)

target_include_directories(
    Castor.Engine.Host.Tests
    PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}/src
)

target_compile_features(
    Castor.Engine.Host.Tests
    PRIVATE
        cxx_std_20
)

add_test(
    NAME Castor.Engine.Host.MainScene
    COMMAND Castor.Engine.Host.Tests
)
