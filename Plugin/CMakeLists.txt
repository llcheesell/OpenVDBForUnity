cmake_minimum_required(VERSION 3.12)
project(OpenVDBNativePlugin)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

# Find system-installed dependencies
find_package(OpenVDB REQUIRED)
find_package(TBB REQUIRED)
find_package(Imath QUIET)

list(APPEND CMAKE_MODULE_PATH "${CMAKE_CURRENT_SOURCE_DIR}/cmake")
include(AddPlugin)

add_subdirectory(openvdbi)
