*** Settings ***
Documentation    Development Player callbacks; isolated saves; actual process restart. No native pointer claim.
Library    ../libraries/GameBusiness.py
Suite Setup    Prepare Isolated Game
Suite Teardown    Close Owned Game
Test Tags    business    player    regression

*** Test Cases ***
Pet Purchase Save And Restart
    Launch Game
    Start New Game
    Pet Cat    200
    Buy Milk Cat
    Assert Milk Count    2
    Save And Quit Game
    Launch Game
    Continue Saved Game
    Assert Milk Count    2
    Save And Quit Game
