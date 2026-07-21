get_filename_component(
    CASTOR_OBS_SDK_ROOT
    "${CMAKE_CURRENT_LIST_DIR}/.."
    ABSOLUTE
)

set(CASTOR_OBS_INCLUDE_DIR
    "${CASTOR_OBS_SDK_ROOT}/include"
)

set(CASTOR_OBS_LIBRARY
    "${CASTOR_OBS_SDK_ROOT}/lib/obs.lib"
)

set(CASTOR_OBS_RUNTIME
    "${CASTOR_OBS_SDK_ROOT}/runtime/bin/64bit/obs.dll"
)

set(CASTOR_PTHREADS_LIBRARY
    "${CASTOR_OBS_SDK_ROOT}/lib/w32-pthreads.lib"
)

set(CASTOR_PTHREADS_RUNTIME
    "${CASTOR_OBS_SDK_ROOT}/runtime/bin/64bit/w32-pthreads.dll"
)

foreach(required_path
    "${CASTOR_OBS_INCLUDE_DIR}"
    "${CASTOR_OBS_LIBRARY}"
    "${CASTOR_OBS_RUNTIME}"
    "${CASTOR_PTHREADS_LIBRARY}"
    "${CASTOR_PTHREADS_RUNTIME}"
)
    if(NOT EXISTS "${required_path}")
        message(FATAL_ERROR
            "Castor OBS SDK file was not found: ${required_path}"
        )
    endif()
endforeach()

if(NOT TARGET OBS::w32-pthreads)
    add_library(OBS::w32-pthreads SHARED IMPORTED)

    set_target_properties(
        OBS::w32-pthreads
        PROPERTIES
            IMPORTED_IMPLIB "${CASTOR_PTHREADS_LIBRARY}"
            IMPORTED_LOCATION "${CASTOR_PTHREADS_RUNTIME}"
            INTERFACE_INCLUDE_DIRECTORIES "${CASTOR_OBS_INCLUDE_DIR}"
    )
endif()

if(NOT TARGET OBS::libobs)
    add_library(OBS::libobs SHARED IMPORTED)

    set_target_properties(
        OBS::libobs
        PROPERTIES
            IMPORTED_IMPLIB "${CASTOR_OBS_LIBRARY}"
            IMPORTED_LOCATION "${CASTOR_OBS_RUNTIME}"
            INTERFACE_INCLUDE_DIRECTORIES "${CASTOR_OBS_INCLUDE_DIR}"
            INTERFACE_COMPILE_DEFINITIONS HAVE_OBSCONFIG_H
            INTERFACE_LINK_LIBRARIES OBS::w32-pthreads
    )
endif()

set(libobs_FOUND TRUE)
