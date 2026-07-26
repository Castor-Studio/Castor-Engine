cmake_minimum_required(VERSION 3.25)

if (POLICY CMP0141)
  cmake_policy(SET CMP0141 NEW)
  set(CMAKE_MSVC_DEBUG_INFORMATION_FORMAT "$<IF:$<AND:$<C_COMPILER_ID:MSVC>,$<CXX_COMPILER_ID:MSVC>>,$<$<CONFIG:Debug,RelWithDebInfo>:EditAndContinue>,$<$<CONFIG:Debug,RelWithDebInfo>:ProgramDatabase>>")
endif()


project(
    CastorEngine
    VERSION 0.1.0
    LANGUAGES C CXX
)

# Check for x64 MSVC environment
if(WIN32 AND NOT CMAKE_SIZEOF_VOID_P EQUAL 8)
    message(
        FATAL_ERROR
        "Castor Engine requires an x64 MSVC environment. "
        "Initialize Developer PowerShell with "
        "-Arch amd64 -HostArch amd64."
    )
endif()

enable_testing()

include(cmake/dependencies/CastorObsSdkVersion.cmake)
include(cmake/dependencies/BootstrapCastorObsSdk.cmake)

castor_bootstrap_obs_sdk()

add_subdirectory(Castor.Engine.Host)
