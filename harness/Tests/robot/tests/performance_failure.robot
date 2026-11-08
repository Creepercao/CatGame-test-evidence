*** Settings ***
Library    Process
Test Tags    performance    harness-contract    expected-failure

*** Test Cases ***
Failed Sampling Retains Partial Frames And Releases Owned Player
    ${run}=    Run Process    python    tools/qa/verify_performance_failure.py    ${OUTPUT DIR}/failure-proof    stdout=${OUTPUT DIR}/failure-contract.log    stderr=STDOUT    timeout=140s    on_timeout=terminate
    Should Be Equal As Integers    ${run.rc}    0
