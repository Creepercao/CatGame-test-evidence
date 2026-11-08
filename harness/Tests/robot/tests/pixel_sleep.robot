*** Settings ***
Library    ../libraries/UnityTestRunner.py    ${UNITY_EXE}    ${PROJECT_PATH}    ${OUTPUT DIR}/unity

*** Variables ***
${UNITY_EXE}    D:/Unity/2022.3.21f1/Editor/Unity.exe
${PROJECT_PATH}    <PROJECT>

*** Test Cases ***
Egypt Occasionally Sleeps Wakes On Click And Resumes Walking
    ${result}=    Run Tests    PlayMode    PixelCatSleepTests
    Should Be Equal As Integers    ${result}[exit_code]    0
    Should Be Equal As Integers    ${result}[total]    4
    Should Be Equal As Integers    ${result}[passed]    4
    Should Be Equal As Integers    ${result}[failed]    0
