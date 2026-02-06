#include "app/App.h"

int APIENTRY wWinMain(HINSTANCE hInstance, HINSTANCE, LPWSTR, int nCmdShow)
{
    App app;
    return app.Run(hInstance, nCmdShow);
}
