*** Settings ***
Documentation    Unity logic smoke; does not claim native pointer or standalone Player coverage.
Library    ../libraries/UnityTestRunner.py    ${UNITY_EXE}    ${PROJECT_PATH}    ${OUTPUT DIR}/unity-smoke
Test Tags    smoke    unity    logic

*** Variables ***
${UNITY_EXE}    %{UNITY_EXE=D:\Unity\2022.3.21f1\Editor\Unity.exe}
${PROJECT_PATH}    %{CATGAME_PROJECT=<PROJECT>}

*** Test Cases ***
Economy Rules Smoke
    ${result}=    Run Tests    EditMode    CatGame.EditTests.IdleMathTests
    Should Be Equal As Integers    ${result}[exit_code]    0
    Should Be Equal As Integers    ${result}[failed]    0
    Should Be Equal As Integers    ${result}[skipped]    0
    Should Be True    ${result}[passed] > 0

Purchase And Upgrade Smoke
    ${result}=    Run Tests    PlayMode    CatGame.Tests.ShopAndItemUpgradeTests
    Should Be Equal As Integers    ${result}[exit_code]    0
    Should Be Equal As Integers    ${result}[failed]    0
    Should Be Equal As Integers    ${result}[skipped]    0
    Should Be True    ${result}[passed] > 0
