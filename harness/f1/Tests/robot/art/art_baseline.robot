*** Settings ***
Documentation    Collect native art snapshots with frozen poses; this is not a gameplay or performance pass.
Library          Process
Library          OperatingSystem

*** Variables ***
${EXPECT_WORLD_GRID}    0

*** Test Cases ***
F1-BASELINE-01 Capture Native Source Geometry And Screens
    Create Directory    ${OUTPUT DIR}/runtime
    ${process}=    Run Process    ${EXE}    -screen-fullscreen    0    -logFile    ${OUTPUT DIR}/player.log
    ...    env:CATGAME_PLAYER_QA_ROOT=${OUTPUT DIR}/runtime
    ...    env:CATGAME_QA_F1_ONLY=1    env:CATGAME_F1_SOURCE=${SOURCE}
    ...    env:CATGAME_DEBUG_DISABLE=1
    ...    stdout=${OUTPUT DIR}/stdout.txt    stderr=${OUTPUT DIR}/stderr.txt
    ...    timeout=120 seconds    on_timeout=terminate
    Should Be Equal As Integers    ${process.rc}    0
    ${raw}=    Get File    ${OUTPUT DIR}/runtime/art-metrics.json
    ${report}=    Evaluate    json.loads($raw)    modules=json
    Should Be True    ${report}[complete]
    Should Be True    ${report}[requestedSizesHonored]
    Should Be True    ${report}[simulationFrozenForSnapshots]
    Length Should Be    ${report}[screens]    3
    Length Should Be    ${report}[poses]    44
    ${screens}=    Set Variable    ${report}[screens]
    FOR    ${sample}    IN    @{screens}
        Should Be True    $sample['frameEnd'] > $sample['frameStart']
        Length Should Be    ${sample}[cats]    11
        File Should Exist    ${OUTPUT DIR}/runtime/${sample}[screenshot]
        IF    '${EXPECT_WORLD_GRID}' == '1'
            Should Be True    $sample['worldRenderCount'] > 0
            Should Be Equal    ${sample}[logicalRaster]    ${sample}[actualRaster]
            Should Be Equal As Integers    ${sample}[cameraAssetsPpu]    32
            Should Be True    abs($sample['cameraPixelRect']['width'] - $sample['width']) < .1
            Should Be True    abs($sample['cameraPixelRect']['height'] - $sample['height']) < .1
            ${expected}=    Evaluate    $sample['height'] / 5.625
            FOR    ${cat}    IN    @{sample}[cats]
                Should Be True    abs($cat['screenBounds']['height'] - $expected) < .1
                Should Be True    abs($cat['footFromSprite']['x'] - $cat['objectPosition']['x']) < .0001
                Should Be True    abs($cat['footFromSprite']['y'] - $cat['objectPosition']['y']) < .0001
            END
        END
    END
