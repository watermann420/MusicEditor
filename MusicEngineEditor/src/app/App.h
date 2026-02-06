#pragma once

#include <windows.h>
#include "editor/EditorView.h"

class App
{
public:
    int Run(HINSTANCE instance, int nCmdShow);

private:
    static LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam);
    bool InitWindow(HINSTANCE instance, int nCmdShow);

    HWND _window = nullptr;
    HBRUSH _backgroundBrush = nullptr;
    EditorView _editor;
};
