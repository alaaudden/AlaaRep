"""
Tests for 4D Modeling view and timeline/tile slipper bar.
"""

import os
from src.xer_parser import parse_xer
from src.progress_analyzer import compare_schedules
from src.dashboard import generate_html_dashboard


def test_4d_modeling_markup_and_components():
    baseline_path = "samples/baseline_project.xer"
    update_path = "samples/update_week12.xer"
    assert os.path.exists(baseline_path)
    assert os.path.exists(update_path)

    base = parse_xer(baseline_path)
    upd = parse_xer(update_path)
    analysis = compare_schedules(base, upd)

    html = generate_html_dashboard(analysis)

    # 1. Navigation title & link in left panel
    assert 'id="nav-fourd"' in html
    assert "4D Modeling" in html

    # 2. Dedicated 4D modeling view panel
    assert 'id="view-fourd"' in html

    # 3. Timeline / tile slipper bar controls
    assert 'id="fourdTimelineSlider"' in html
    assert "Time Slipper Bar" in html
    assert 'id="fourdBtnPlay"' in html
    assert "Play Simulation" in html
    assert 'id="fourdSimDateBadge"' in html
    assert 'id="fourdSimPctBadge"' in html

    # 4. 4D Model SVG viewport & structural tiles
    assert 'id="fourdModelSvg"' in html
    assert 'id="fourdTile-ZONE-SUB"' in html
    assert 'id="fourdTile-ZONE-TOW"' in html
    assert 'id="fourdTile-ZONE-MEP"' in html
    assert 'id="fourdTile-ZONE-FIN"' in html
    assert 'id="fourdTile-ZONE-SITE"' in html

    # 5. Tile status indicator cards
    assert 'id="tileCard-ZONE-SUB"' in html
    assert 'id="tileCard-ZONE-TOW"' in html
    assert 'id="tileCard-ZONE-MEP"' in html
    assert 'id="tileCard-ZONE-FIN"' in html
    assert 'id="tileCard-ZONE-SITE"' in html

    # 6. Simulation engine JavaScript logic
    assert "initFourdSimulation" in html
    assert "onFourdSliderChange" in html
    assert "updateFourdElementsState" in html
    assert "selectFourdTile" in html
    assert "toggleFourdPlay" in html
    assert "stepFourdSlider" in html


def test_dist_html_has_4d_simulation():
    dist_file = "dist/progress_dashboard.html"
    assert os.path.exists(dist_file)
    with open(dist_file, "r", encoding="utf-8") as f:
        html = f.read()

    assert 'id="nav-fourd"' in html
    assert 'id="view-fourd"' in html
    assert 'id="fourdTimelineSlider"' in html
    assert "Time Slipper Bar" in html
    assert "4D Construction Timeline Simulation & Tile Slipper" in html
