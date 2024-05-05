# CMakeList.txt : CMake project for GameX.Bethesda.Native, include source and define
# project specific logic here.
#

# Add source to this project's executable.
add_executable (GameX.Bethesda.Native "GameX.Bethesda.Native.cpp" "GameX.Bethesda.Native.h")

if (CMAKE_VERSION VERSION_GREATER 3.12)
  set_property(TARGET GameX.Bethesda.Native PROPERTY CXX_STANDARD 20)
endif()

# 
#	VS_DOTNET_TARGET_FRAMEWORK_VERSION "v4.6"
#COMPILE_FLAGS "/clr"
#VS_DOTNET_REFERENCES "System"
set_target_properties(GameX.Bethesda.Native PROPERTIES
	COMMON_LANGUAGE_RUNTIME ""
	)
#string(REGEX REPLACE "/RTC(su|[1su])" "" CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS}")
#string(REGEX REPLACE "/RTC(su|[1su])" "" CMAKE_CXX_FLAGS_DEBUG "${CMAKE_CXX_FLAGS_DEBUG}")


# TODO: Add tests and install targets if needed.
