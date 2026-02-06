#pragma once

#ifndef NOMINMAX
#define NOMINMAX
#endif

#include <windows.h>
#include <string>
#include "editor/EditorView.h"
#include "app/MusicEngineHost.h"

class App
{
public:
    int Run(HINSTANCE instance, int nCmdShow);

private:
    static LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam);
    bool InitWindow(HINSTANCE instance, int nCmdShow);
    void LayoutControls();
    void TogglePlayback();
    void StartPlayback();
    void StopPlayback();
    void RefreshPlayback();
    void ToggleConsole();
    void UpdatePlayButton();
    bool IsOverSplitter(POINT pt) const;
    void AppendConsoleText(const std::wstring& text);
    bool ShouldSuppressConsoleLine(const std::wstring& line) const;
    void TickEditorVisuals();

    HWND _window = nullptr;
    HWND _playButton = nullptr;
    HWND _consoleToggleButton = nullptr;
    HWND _console = nullptr;
    HFONT _uiFont = nullptr;
    HBRUSH _backgroundBrush = nullptr;
    HBRUSH _topBarBrush = nullptr;
    HBRUSH _consoleBrush = nullptr;
    bool _isPlaying = false;
    bool _consoleVisible = true;
    bool _draggingSplitter = false;
    int _consoleHeight = 160;
    std::wstring _consolePending;
    EditorView _editor;
    MusicEngineHost _engine;
};
