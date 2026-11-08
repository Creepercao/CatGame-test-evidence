*** Settings ***
Library    Process
Test Tags    performance    probe    player

*** Test Cases ***
Performance Counters Are Available In Isolated Player
    ${run}=    Run Process    python    tools/qa/run_performance.py    ${OUTPUT DIR}/performance    --probe    stdout=${OUTPUT DIR}/performance.log    stderr=STDOUT    timeout=120s    on_timeout=terminate
    Should Be Equal As Integers    ${run.rc}    0
