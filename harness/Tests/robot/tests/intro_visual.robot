*** Settings ***
Library    ../libraries/UnityTestRunner.py    D:/Unity/2022.3.21f1/Editor/Unity.exe    <PROJECT>    ${OUTPUT DIR}/unity

*** Test Cases ***
Pixel Intro Portrait And Dialogue Behaviors Pass
    ${result}=    Run Tests    PlayMode    CatGame.Tests.IntroDialogueTests
    Should Be Equal As Integers    ${result}[failed]    0
    Should Be Equal As Integers    ${result}[passed]    4
    Should Be Equal As Integers    ${result}[exit_code]    0
