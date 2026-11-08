*** Settings ***
Library    ../libraries/UnityTestRunner.py    ${UNITY_EXE}    ${PROJECT_PATH}    ${OUTPUT DIR}/unity

*** Variables ***
${UNITY_EXE}    D:/Unity/2022.3.21f1/Editor/Unity.exe
${PROJECT_PATH}    <PROJECT>

*** Test Cases ***
Egypt Pixel Animations Load Transition And Render In Unity
    ${result}=    Run Tests    PlayMode    PixelCatIntegrationTests
    Should Be Equal As Integers    ${result}[exit_code]    0
    Should Be Equal As Integers    ${result}[total]    3
    Should Be Equal As Integers    ${result}[passed]    3
    Should Be Equal As Integers    ${result}[failed]    0
