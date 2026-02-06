cmake_minimum_required(VERSION 3.20)
project(MusicEngineEditorApp LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

include(FetchContent)

set(SDL_SHARED OFF CACHE BOOL "" FORCE)
set(SDL_STATIC ON CACHE BOOL "" FORCE)
set(SDL_TEST OFF CACHE BOOL "" FORCE)

FetchContent_Declare(
  SDL2
  GIT_REPOSITORY https://github.com/libsdl-org/SDL.git
  GIT_TAG release-2.30.4
)

FetchContent_Declare(
  imgui
  GIT_REPOSITORY https://github.com/ocornut/imgui.git
  GIT_TAG v1.90.9
)

FetchContent_MakeAvailable(SDL2 imgui)

set(IMGUI_DIR ${imgui_SOURCE_DIR})

set(IMGUI_SOURCES
  ${IMGUI_DIR}/imgui.cpp
  ${IMGUI_DIR}/imgui_demo.cpp
  ${IMGUI_DIR}/imgui_draw.cpp
  ${IMGUI_DIR}/imgui_tables.cpp
  ${IMGUI_DIR}/imgui_widgets.cpp
  ${IMGUI_DIR}/backends/imgui_impl_sdl2.cpp
  ${IMGUI_DIR}/backends/imgui_impl_opengl3.cpp
)

add_executable(MusicEngineEditor
  src/main.cpp
  ${IMGUI_SOURCES}
)

target_include_directories(MusicEngineEditor PRIVATE
  ${IMGUI_DIR}
  ${IMGUI_DIR}/backends
)

find_package(OpenGL REQUIRED)

target_link_libraries(MusicEngineEditor PRIVATE SDL2::SDL2 SDL2::SDL2main OpenGL::GL)

if (MSVC)
  target_compile_options(MusicEngineEditor PRIVATE /W4 /permissive-)
else()
  target_compile_options(MusicEngineEditor PRIVATE -Wall -Wextra -Wpedantic)
endif()
