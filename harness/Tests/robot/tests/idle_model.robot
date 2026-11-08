*** Settings ***
Documentation     Offline numerical regression and Unity Mono compile checks; does not execute Unity Test Runner or JsonUtility behavior.
Library           Process
Library           OperatingSystem

*** Variables ***
${PROJECT_PATH}    %{CATGAME_PROJECT=<PROJECT>}
${UNITY_MONO}    %{UNITY_MONO=D:\Unity\2022.3.21f1\Editor\Data\MonoBleedingEdge\bin\mono.exe}
${UNITY_MCS}    %{UNITY_MCS=D:\Unity\2022.3.21f1\Editor\Data\MonoBleedingEdge\lib\mono\4.5\mcs.exe}
${NUNIT_DLL}    Library/PackageCache/com.unity.ext.nunit@1.0.6/net35/unity-custom/nunit.framework.dll
${NUNIT_METHOD_RUNNER}    .curve-runner/bin/Debug/net8.0/CatGameIdleNUnitMethods.exe
${NETSTANDARD_FACADE}    D:/Unity/2022.3.21f1/Editor/Data/MonoBleedingEdge/lib/mono/4.5/Facades/netstandard.dll
${UNITY_CORE_DLL}    D:/Unity/2022.3.21f1/Editor/Data/Managed/UnityEngine/UnityEngine.CoreModule.dll
${UNITY_JSON_DLL}    D:/Unity/2022.3.21f1/Editor/Data/Managed/UnityEngine/UnityEngine.JSONSerializeModule.dll
${UNITY_UI_MODULE_DLL}    D:/Unity/2022.3.21f1/Editor/Data/Managed/UnityEngine/UnityEngine.UIModule.dll
${UNITY_PHYSICS2D_DLL}    D:/Unity/2022.3.21f1/Editor/Data/Managed/UnityEngine/UnityEngine.Physics2DModule.dll
${UNITY_TEXT_DLL}    D:/Unity/2022.3.21f1/Editor/Data/Managed/UnityEngine/UnityEngine.TextRenderingModule.dll
${UNITY_INPUT_DLL}    D:/Unity/2022.3.21f1/Editor/Data/Managed/UnityEngine/UnityEngine.InputLegacyModule.dll
${UNITY_AUDIO_DLL}    D:/Unity/2022.3.21f1/Editor/Data/Managed/UnityEngine/UnityEngine.AudioModule.dll
${UNITY_TILEMAP_DLL}    D:/Unity/2022.3.21f1/Editor/Data/Managed/UnityEngine/UnityEngine.TilemapModule.dll
${UNITY_GRID_DLL}    D:/Unity/2022.3.21f1/Editor/Data/Managed/UnityEngine/UnityEngine.GridModule.dll
${UNITY_UI_ASSEMBLY}    Library/ScriptAssemblies/UnityEngine.UI.dll
${UNITY_PIXEL_PERFECT_ASSEMBLY}    Library/ScriptAssemblies/Unity.2D.PixelPerfect.dll
${UNITY_TEST_RUNNER_DLL}    Library/ScriptAssemblies/UnityEngine.TestRunner.dll
${UNITY_EDITOR_TEST_RUNNER_DLL}    Library/ScriptAssemblies/UnityEditor.TestRunner.dll
${SAVE_RUNTIME_COMPILE_DLL}    .curve-runner/bin/Debug/net8.0/CatGameRuntimeCompile.dll
${SAVE_TEST_COMPILE_DLL}    .curve-runner/bin/Debug/net8.0/CatGamePlaySaveTestsCompile.dll

*** Test Cases ***
Idle math and curve gates pass in the shared C# model
    [Documentation]    Verify shared economy code and NUnit methods; compile save runtime/tests against Unity assemblies without executing Unity APIs.
    ${build}=    Run Process    dotnet    build    .curve-runner/.curve-runner.csproj    --no-restore    --nologo    -v:q    cwd=${PROJECT_PATH}    stdout=PIPE    stderr=STDOUT
    Log    ${build.stdout}
    Should Be Equal As Integers    ${build.rc}    0    msg=${build.stdout}
    ${result}=    Run Process    dotnet    .curve-runner/bin/Debug/net8.0/.curve-runner.dll    cwd=${PROJECT_PATH}    stdout=PIPE    stderr=STDOUT
    Log    ${result.stdout}
    Should Be Equal As Integers    ${result.rc}    0    msg=${result.stdout}
    Should Contain    ${result.stdout}    ECON-01 catalog counts: PASS
    Should Contain    ${result.stdout}    ECON-02 cat payback: PASS
    Should Contain    ${result.stdout}    ECON-03 cat cap and price: PASS
    Should Contain    ${result.stdout}    ECON-04 item recurrences: PASS
    Should Contain    ${result.stdout}    ECON-05 additive exponential item levels: PASS
    Should Contain    ${result.stdout}    ECON-05b item level effects match design table: PASS
    Should Contain    ${result.stdout}    ECON-06 stage click and offline cap: PASS
    Should Contain    ${result.stdout}    ECON-07 curve gates: PASS
    Should Contain    ${result.stdout}    ECON-08 simulator completion state: PASS
    Should Contain    ${result.stdout}    ECON-09 invalid click rates rejected: PASS
    ${nunit_build}=    Run Process    ${UNITY_MONO}    ${UNITY_MCS}    -langversion:7.2    -out:${NUNIT_METHOD_RUNNER}    -r:${NUNIT_DLL}    Assets/Scripts/Idle/AchievementCatalog.cs    Assets/Scripts/Idle/OfflineEarnings.cs    Assets/Scripts/Idle/GameState.cs    Assets/Scripts/Idle/GameStateFactory.cs    Assets/Scripts/Idle/IdleMath.cs    Assets/Scripts/Idle/CatCatalog.cs    Assets/Scripts/Idle/ItemCatalog.cs    Assets/Scripts/Idle/IdleBalance.cs    Assets/Tests/EditMode/IdleMathTests.cs    Tests/robot/tools/RunNUnitMethods.cs    cwd=${PROJECT_PATH}    stdout=PIPE    stderr=STDOUT
    Log    ${nunit_build.stdout}
    Should Be Equal As Integers    ${nunit_build.rc}    0    msg=${nunit_build.stdout}
    ${nunit_result}=    Run Process    ${UNITY_MONO}    ${NUNIT_METHOD_RUNNER}    cwd=${PROJECT_PATH}    env:MONO_PATH=Library/PackageCache/com.unity.ext.nunit@1.0.6/net35/unity-custom    stdout=PIPE    stderr=STDOUT
    Log    ${nunit_result.stdout}
    Should Be Equal As Integers    ${nunit_result.rc}    0    msg=${nunit_result.stdout}
    Should Contain    ${nunit_result.stdout}    NUNIT-MONO: 14/14 NUnit assertion methods passed; Unity Test Runner not used
    Should Contain    ${nunit_result.stdout}    STR-03 ticks=100000
    ${save_runtime_compile}=    Run Process    ${UNITY_MONO}    ${UNITY_MCS}    -langversion:7.2    -target:library    -out:${SAVE_RUNTIME_COMPILE_DLL}    -r:${NETSTANDARD_FACADE}    -r:${UNITY_CORE_DLL}    -r:${UNITY_JSON_DLL}    -r:${UNITY_UI_MODULE_DLL}    -r:${UNITY_PHYSICS2D_DLL}    -r:${UNITY_TEXT_DLL}    -r:${UNITY_INPUT_DLL}    -r:${UNITY_AUDIO_DLL}    -r:${UNITY_GRID_DLL}    -r:${UNITY_TILEMAP_DLL}    -r:${UNITY_UI_ASSEMBLY}    -r:${UNITY_PIXEL_PERFECT_ASSEMBLY}    Assets/Scripts/AchievementToast.cs    Assets/Scripts/CatActor.cs    Assets/Scripts/CatGame.cs    Assets/Scripts/CatRoster.cs    Assets/Scripts/CatVisualLibrary.cs    Assets/Scripts/CatWorld.cs    Assets/Scripts/PlatformGrid.cs    Assets/Scripts/GameData.cs    Assets/Scripts/Hud.cs    Assets/Scripts/IdleGameController.cs    Assets/Scripts/MainMenuUI.cs    Assets/Scripts/PausePanel.cs    Assets/Scripts/SceneFlow.cs    Assets/Scripts/UiKit.cs    Assets/Scripts/Idle/AchievementCatalog.cs    Assets/Scripts/Idle/OfflineEarnings.cs    Assets/Scripts/Idle/GameState.cs    Assets/Scripts/Idle/GameStateFactory.cs    Assets/Scripts/Idle/IdleBalance.cs    Assets/Scripts/Idle/CatCatalog.cs    Assets/Scripts/Idle/ItemCatalog.cs    Assets/Scripts/Idle/IdleMath.cs    Assets/Scripts/Idle/SaveSystem.cs    cwd=${PROJECT_PATH}    stdout=PIPE    stderr=STDOUT
    Log    ${save_runtime_compile.stdout}
    Should Be Equal As Integers    ${save_runtime_compile.rc}    0    msg=${save_runtime_compile.stdout}
    ${save_test_compile}=    Run Process    ${UNITY_MONO}    ${UNITY_MCS}    -langversion:7.2    -target:library    -out:${SAVE_TEST_COMPILE_DLL}    -r:${NETSTANDARD_FACADE}    -r:${UNITY_CORE_DLL}    -r:${UNITY_JSON_DLL}    -r:${UNITY_UI_MODULE_DLL}    -r:${UNITY_PHYSICS2D_DLL}    -r:${UNITY_TEXT_DLL}    -r:${UNITY_INPUT_DLL}    -r:${UNITY_AUDIO_DLL}    -r:${UNITY_GRID_DLL}    -r:${UNITY_TILEMAP_DLL}    -r:${UNITY_UI_ASSEMBLY}    -r:${UNITY_PIXEL_PERFECT_ASSEMBLY}    -r:${NUNIT_DLL}    -r:${UNITY_TEST_RUNNER_DLL}    -r:${UNITY_EDITOR_TEST_RUNNER_DLL}    -r:${SAVE_RUNTIME_COMPILE_DLL}    Assets/Tests/PlayMode/CatGameTestBase.cs    Assets/Tests/PlayMode/AnimationTests.cs    Assets/Tests/PlayMode/UiFlowTests.cs    Assets/Tests/PlayMode/CatRosterTests.cs    Assets/Tests/EditMode/SaveSystemTests.cs    cwd=${PROJECT_PATH}    stdout=PIPE    stderr=STDOUT
    Log    ${save_test_compile.stdout}
    Should Be Equal As Integers    ${save_test_compile.rc}    0    msg=${save_test_compile.stdout}

    ${setup_source}=    Get File    Assets/Editor/CatGameSetup.cs
    Should Contain    ${setup_source}    SceneBuilders.MenuPath,
    Should Contain    ${setup_source}    SceneBuilders.HomePath,
    Should Not Contain    ${setup_source}    SceneBuilders.ParkPath,
    ${builder_source}=    Get File    Assets/Editor/SceneBuilders.cs
    Should Contain    ${builder_source}    BuildPark();
    Should Contain    ${builder_source}    new EditorBuildSettingsScene(MenuPath, true)
    Should Contain    ${builder_source}    new EditorBuildSettingsScene(HomePath, true)
    Should Not Contain    ${builder_source}    new EditorBuildSettingsScene(ParkPath, true)
