"""The SegmentTiles log line must let a client report be matched by volume coordinates."""

from segmentation_server.prompt_log import describe_prompts


def test_reports_first_foreground_point_box_and_background() -> None:
    text = describe_prompts([(100, 200), (90, 210), (120, 190)], [(5, 6), (7, 8)])

    assert text == "fg0=(100,200) fgbox=(90,190)-(120,210) bg=[(5,6),(7,8)]"


def test_no_foreground_and_no_background() -> None:
    assert describe_prompts([], []) == "fg0=none fgbox=none bg=[]"


def test_long_background_list_is_truncated_with_a_count() -> None:
    background = [(i, i) for i in range(15)]

    text = describe_prompts([(1, 1)], background, max_background=12)

    assert text.endswith("(11,11)+3]")
    assert "(12,12)" not in text
