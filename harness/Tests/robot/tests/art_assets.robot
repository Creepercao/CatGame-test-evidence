*** Settings ***
Documentation    Full EditMode validation after asset/test-expectation changes; no PlayMode rerun.
Library    ../libraries/UnityTestRunner.py    ${UNITY_EXE}    ${PROJECT_PATH}    ${OUTPUT DIR}/unity

*** Variables ***
${UNITY_EXE}    D:/Unity/2022.3.21f1/Editor/Unity.exe
${PROJECT_PATH}    <PROJECT>

*** Test Cases ***
All EditMode Asset Scene Economy And Save Checks Pass
    ${result}=    Run Tests    EditMode
    Should Be Equal As Integers    ${result}[exit_code]    0
    Should Be Equal As Integers    ${result}[total]    36
    Should Be Equal As Integers    ${result}[passed]    36
    Should Be Equal As Integers    ${result}[failed]    0
    Should Be Equal As Integers    ${result}[skipped]    0
