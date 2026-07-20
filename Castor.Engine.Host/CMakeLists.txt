add_library(Castor.Engine.Host SHARED
    src/castor_engine.cpp
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
