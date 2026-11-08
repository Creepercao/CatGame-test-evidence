*** Settings ***
Documentation    Deliberate assertion failure for evidence validation; excluded from normal profiles.
Library    ../libraries/GameBusiness.py
Suite Setup    Prepare Isolated Game
Suite Teardown    Close Owned Game
Test Tags    harness-contract    expected-failure

*** Test Cases ***
Deliberate Wrong Ownership Expectation
    Launch Game
    Start New Game
    Assert Milk Count    99
