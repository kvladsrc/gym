import io
import json

import numpy as np
import pytest
from PIL import Image

from shotsearch import review_range
from shotsearch.search import Filters


def games(answer):
    return [r["game"]["appid"] for r in answer["results"]]


def test_build_manifest_and_games(service, index_dir):
    manifest = json.loads((index_dir / "manifest.json").read_text())
    assert manifest["shots"] == 28
    assert manifest["games"] == 6
    assert manifest["shots_without_game"] == 4
    assert manifest["forecasts"] == 18
    for space in ("meaning", "look"):
        c = manifest["calibration"][space]
        assert c["same_game_median"] > c["random_median"]
        vectors = np.asarray(service.index.vectors[space], dtype=np.float32)
        assert np.allclose(np.linalg.norm(vectors, axis=1), 1, atol=1e-2)
    by_id = {g["appid"]: g for g in service.index.games}
    assert by_id[14]["name"] == "Game 14"  # the first file gives the data
    assert by_id[14]["seen"] and by_id[11]["seen"] and not by_id[10]["seen"]


def test_shot_query_leaves_out_its_game_and_unknown_shots(service):
    answer = service.search(shots=["10_00"], limit=5, pool=5)
    assert 10 not in games(answer)
    assert 99 not in games(answer)
    assert len(games(answer)) == len(set(games(answer))) == 5


def test_rankings_follow_their_space(service):
    assert games(service.search(shots=["10_00"], ranking="meaning", limit=1, pool=1)) == [11]
    assert games(service.search(shots=["10_00"], ranking="look", limit=1, pool=1)) == [12]
    both = service.search(shots=["10_00"], limit=5, pool=5)["results"]
    keys = [(r["closeness"]["meaning"] + r["closeness"]["look"]) / 2 for r in both]
    assert keys == sorted(keys, reverse=True)


def test_include_query_games_and_all_shots(service):
    answer = service.search(
        shots=["10_00"], include_query_games=True, per_game=False, limit=3, pool=3
    )
    assert games(answer) == [10, 10, 10]
    assert "10_00" not in [r["shot"] for r in answer["results"]]


def test_filters(service):
    horror = service.search(shots=["12_00"], filters=Filters(tags=("horror",)), limit=3, pool=3)
    assert sorted(games(horror)) == [10, 11, 13]
    recent = service.search(shots=["12_00"], filters=Filters(year_from=2020, year_to=2021), pool=5)
    assert sorted(games(recent)) == [10, 11]
    unseen = service.search(shots=["12_00"], filters=Filters(unseen_only=True), pool=5)
    assert sorted(games(unseen)) == [10, 15]
    popular = service.search(shots=["12_00"], filters=Filters(min_reviews=1000), pool=5)
    assert sorted(games(popular)) == [11, 13]


def test_sort_by_reviews_and_forecast(service):
    by_reviews = service.search(shots=["15_00"], sort="reviews", limit=5, pool=5)["results"]
    reviews = [r["game"]["reviews"] for r in by_reviews]
    assert reviews == sorted(reviews, reverse=True)
    by_forecast = service.search(
        shots=["15_00"], sort="forecast", per_game=False, limit=20, pool=20
    )["results"]
    classes = [r["forecast"]["review_class"] if r["forecast"] else -1 for r in by_forecast]
    assert classes == sorted(classes, reverse=True)
    assert classes[-1] == -1  # shots without a forecast go last


def test_result_fields(service):
    result = service.search(shots=["10_00"], ranking="meaning", limit=1, pool=1)["results"][0]
    assert result["steam_url"] == "https://store.steampowered.com/app/11/"
    assert result["image"].endswith(f"{result['shot']}.jpg")
    if result["forecast"]:
        f = result["forecast"]
        assert f["reviews"] == review_range(f["review_class"])
        assert f["game_seen_in_training"] is True


def test_validation(service):
    with pytest.raises(ValueError):
        service.search()
    with pytest.raises(ValueError):
        service.search(shots=["10_00"], limit=0)
    with pytest.raises(ValueError):
        service.search(shots=["10_00"], pool=5000)
    with pytest.raises(ValueError):
        service.search(shots=["10_00"], ranking="colour")
    with pytest.raises(KeyError):
        service.search(shots=["77_00"])


def test_sheet(service):
    answer = service.search(shots=["10_00", "10_01"], limit=4, pool=4, sheet=True)
    with Image.open(answer["sheet"]) as sheet:
        assert sheet.width > 1000


def test_game(service):
    game = service.game(10)
    assert [s["shot"] for s in game["shots"]] == ["10_00", "10_01", "10_02", "10_03"]
    assert game["shots"][3]["forecast"] is None
    with pytest.raises(KeyError):
        service.game(99)


def test_upload(service):
    data = io.BytesIO()
    Image.new("RGB", (40, 30), (200, 10, 10)).save(data, "PNG")
    answer = service.upload(data.getvalue())
    assert service.upload_path(answer["id"]).exists()
    with pytest.raises(ValueError):
        service.upload(b"not an image")
    with pytest.raises(ValueError):
        service.upload_path("../../etc/passwd")


def test_text_rank_uses_raw_similarity(service):
    query = np.asarray(service.index.vectors["meaning"][0], dtype=np.float32)
    results = service.searcher.search(
        {"meaning": query}, limit=3, pool=3, uncalibrated=frozenset({"meaning"})
    )
    assert all(r["closeness"] == {} and "meaning" in r["similarity"] for r in results)
    sims = [r["similarity"]["meaning"] for r in results]
    assert sims == sorted(sims, reverse=True)
    with pytest.raises(ValueError):
        service.search(shots=["10_00"], text="a dark alley")


def test_nothing_left_after_filters(service):
    assert service.search(shots=["10_00"], filters=Filters(year_from=2090))["results"] == []


def test_filters_describe():
    text = Filters(year_from=2020, tags=("Horror",), unseen_only=True).describe()
    assert text == "years 2020-; tags Horror; games the forecaster never saw"
