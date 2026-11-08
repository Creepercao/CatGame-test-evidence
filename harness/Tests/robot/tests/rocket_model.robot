*** Settings ***
Library    Process
Library    OperatingSystem
Library    Collections

*** Test Cases ***
Six Routes Buy Rocket With AtLeast Ninety ItemLevels And An Established Endgame
    ${run}=    Run Process    dotnet    run    --project    tools/economy/rocket_curve/RocketCurve.csproj    --    ${OUTPUT DIR}/curves
    ...    cwd=<PROJECT>    timeout=120s    on_timeout=terminate    stdout=${OUTPUT DIR}/model.log    stderr=STDOUT
    Should Be Equal As Integers    ${run.rc}    0
    ${raw}=    Get File    ${OUTPUT DIR}/curves/summary.json
    ${rows}=    Evaluate    json.loads($raw)    modules=json
    Length Should Be    ${rows}    6
    FOR    ${row}    IN    @{rows}
        Should Be True    ${row}[passed]
        Should Be Equal As Numbers    ${row}[rocketPrice]    1000000000000
        Should Be True    ${row}[itemLevels] >= 90
        Should Be Equal As Integers    ${row}[breeds]    11
        Should Be True    ${row}[secondsAfterXmas] > 900
        Should Be Equal As Numbers    ${row}[balanceAfter]    0
    END
