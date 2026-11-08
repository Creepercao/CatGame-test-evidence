*** Settings ***
Library    ../libraries/UnityTestRunner.py    D:/Unity/2022.3.21f1/Editor/Unity.exe    <PROJECT>    ${OUTPUT DIR}/unity

*** Test Cases ***
Rocket Confirmation Ending Recovery Choices AndStress Pass
    ${result}=    Run Tests    PlayMode    CatGame.Tests.RocketEndingTests
    Should Be Equal As Integers    ${result}[failed]    0
    Should Be Equal As Integers    ${result}[passed]    3
    Should Be Equal As Integers    ${result}[exit_code]    0
Economy Transaction Snapshot Persistence And QueueStress Pass
    ${result}=    Run Tests    EditMode    CatGame.EditTests.EndingRulesTests
    Should Be Equal As Integers    ${result}[failed]    0
    Should Be Equal As Integers    ${result}[passed]    4
    Should Be Equal As Integers    ${result}[exit_code]    0
