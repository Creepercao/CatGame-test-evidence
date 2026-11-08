*** Settings ***
Library    ../libraries/UnityTestRunner.py    D:/Unity/2022.3.21f1/Editor/Unity.exe    <PROJECT>    ${OUTPUT DIR}/unity

*** Test Cases ***
Achievement Memories Queue Pause Recover And Show Imported Illustrations
    ${result}=    Run Tests    PlayMode    CatGame.Tests.StoryPlayerTests
    Should Be Equal As Integers    ${result}[failed]    0
    Should Be Equal As Integers    ${result}[passed]    5
    Should Be Equal As Integers    ${result}[exit_code]    0
