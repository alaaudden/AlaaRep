"""
Command-Line Interface (CLI) and entry point for Primavera P6 Progress Dashboard.
Usage:
  python main.py --baseline <baseline.xer> --update <update.xer> [--output <output.html>]
  python main.py --serve [--port 8000]
"""

import argparse
import sys
import os
from src.xer_parser import parse_xer
from src.progress_analyzer import compare_schedules
from src.dashboard import generate_html_dashboard


def main():
    parser = argparse.ArgumentParser(
        description="Primavera P6 Progress Report & Dashboard Generator"
    )
    parser.add_argument("--baseline", "-b", help="Path to Baseline XER file")
    parser.add_argument("--update", "-u", help="Path to Weekly/Current Update XER file")
    parser.add_argument(
        "--output",
        "-o",
        default="dist/progress_dashboard.html",
        help="Path for generated HTML report (default: dist/progress_dashboard.html)",
    )
    parser.add_argument(
        "--serve",
        action="store_true",
        help="Launch the interactive web application server",
    )
    parser.add_argument(
        "--port",
        type=int,
        default=8000,
        help="Port for web server (default: 8000)",
    )
    parser.add_argument(
        "--host",
        default="127.0.0.1",
        help="Host for web server (default: 127.0.0.1)",
    )

    args = parser.parse_args()

    if args.serve:
        import uvicorn
        print(f"Starting Primavera P6 Dashboard Web Server on http://{args.host}:{args.port}")
        uvicorn.run("src.app:app", host=args.host, port=args.port, reload=False)
        return

    # Check for CLI comparison inputs
    baseline_path = args.baseline
    update_path = args.update

    # Default to samples if no args provided
    if not baseline_path or not update_path:
        default_baseline = "samples/baseline_project.xer"
        default_update = "samples/update_week12.xer"
        if os.path.exists(default_baseline) and os.path.exists(default_update):
            print(f"No files specified. Using sample project files:")
            print(f"  Baseline: {default_baseline}")
            print(f"  Update:   {default_update}\n")
            baseline_path = default_baseline
            update_path = default_update
        else:
            parser.print_help()
            sys.exit(1)

    print(f"Parsing Baseline: {baseline_path}...")
    baseline_proj = parse_xer(baseline_path)
    print(f"  Found {len(baseline_proj.tasks)} activities in Baseline.")

    print(f"Parsing Weekly Update: {update_path}...")
    update_proj = parse_xer(update_path)
    print(f"  Found {len(update_proj.tasks)} activities in Update.")

    print("\nAnalyzing progress and comparing baseline...")
    report_data = compare_schedules(baseline_proj, update_proj)

    summary = report_data["summary"]
    proj_info = report_data["project_info"]

    print("\n================ PROGRESS SUMMARY ================")
    print(f"Project Name:           {proj_info['project_name']}")
    print(f"Data Date:              {proj_info['data_date']}")
    print(f"Planned Progress:       {summary['overall_progress_planned']}%")
    print(f"Actual Progress:        {summary['overall_progress_actual']}%")
    print(f"Schedule Variance (SV): {summary['schedule_variance_pct']:+}%")
    print(f"Schedule Index (SPI):   {summary['spi']}")
    print(f"Schedule Status:        {summary['status_indicator']}")
    print(f"Baseline Finish:        {proj_info['baseline_finish']}")
    print(f"Forecast Finish:        {proj_info['forecast_finish']}")
    print(f"Project Slippage:       {proj_info['schedule_slippage_days']} days")
    print("==================================================\n")

    # Generate HTML
    html_content = generate_html_dashboard(report_data)
    os.makedirs(os.path.dirname(os.path.abspath(args.output)), exist_ok=True)
    with open(args.output, "w", encoding="utf-8") as f:
        f.write(html_content)

    print(f"Dashboard successfully generated: {os.path.abspath(args.output)}")


if __name__ == "__main__":
    main()
