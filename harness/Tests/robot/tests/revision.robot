*** Settings ***
Documentation    Targeted v2 regression; full legacy economy suite still needs migration.
Library    ../libraries/UnityTestRunner.py    ${UNITY_EXE}    ${PROJECT_PATH}    ${OUTPUT DIR}/unity-revision

*** Variables ***
${UNITY_EXE}    D:/Unity/2022.3.21f1/Editor/Unity.exe
${PROJECT_PATH}    <PROJECT>

*** Test Cases ***
Revised Purchase Layout Resources And Number Boundaries
    ${result}=    Run Tests    PlayMode    Revision_
    Should Be Equal As Integers    ${result}[total]    2
    Should Be Equal As Integers    ${result}[passed]    2
    Should Be Equal As Integers    ${result}[exit_code]    0

