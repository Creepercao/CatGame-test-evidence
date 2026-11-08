*** Settings ***
Library    Process
Test Tags    performance    stress    player

*** Variables ***
${ROUNDS}    %{CATGAME_PERFORMANCE_ROUNDS=2}

*** Test Cases ***
Five Performance Workloads And Repeated Baseline
    ${run}=    Run Process    python    tools/qa/run_performance.py    ${OUTPUT DIR}/performance    --rounds    ${ROUNDS}    stdout=${OUTPUT DIR}/performance.log    stderr=STDOUT    timeout=1250s    on_timeout=terminate
    Should Be Equal As Integers    ${run.rc}    0
