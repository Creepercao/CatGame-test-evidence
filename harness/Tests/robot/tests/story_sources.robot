*** Settings ***
Library    Process

*** Test Cases ***
Item Opaque Profiles And Generated Story Assets Match Source Files
    ${result}=    Run Process    python    tools/qa/verify_story_and_items.py    ${OUTPUT DIR}/sources    stdout=${OUTPUT DIR}/verification.log    stderr=STDOUT
    Should Be Equal As Integers    ${result.rc}    0
