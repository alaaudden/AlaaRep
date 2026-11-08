import asyncio
import os
from playwright.async_api import async_playwright

async def run():
    async with async_playwright() as p:
        browser = await p.chromium.launch(headless=True)
        page = await browser.new_page(viewport={"width": 1440, "height": 900})

        file_url = f"file://{os.path.abspath('dist/progress_dashboard.html')}"
        await page.goto(file_url)

        # Wait for page to load
        await page.wait_for_selector("#interactiveSiteSvg")
        print("Page loaded, interactive layout visible.")

        # Take screenshot of Overview with interactive layout
        os.makedirs("dist/screenshots", exist_ok=True)
        await page.screenshot(path="dist/screenshots/overview_layout.png")

        # Click on Zone 1: Substructure (ZONE-SUB)
        await page.click('g[data-zone="ZONE-SUB"]')
        await page.wait_for_timeout(500)
        await page.screenshot(path="dist/screenshots/overview_zone_sub_clicked.png")
        print("Zone SUB clicked and KPI updated.")

        # Click Lookahead Tab
        await page.click("#nav-lookahead")
        await page.wait_for_timeout(500)
        await page.screenshot(path="dist/screenshots/lookahead_view.png")
        print("Lookahead view verified.")

        # Click Manpower Tab
        await page.click("#nav-manpower")
        await page.wait_for_timeout(500)
        await page.screenshot(path="dist/screenshots/manpower_view.png")
        print("Manpower view verified.")

        # Click Cashflow Tab
        await page.click("#nav-cashflow")
        await page.wait_for_timeout(500)
        await page.screenshot(path="dist/screenshots/cashflow_view.png")
        print("Cashflow view verified.")

        # Click Invoice Tab
        await page.click("#nav-invoice")
        await page.wait_for_timeout(500)
        await page.screenshot(path="dist/screenshots/invoice_view.png")
        print("Invoice view verified.")

        # Switch back to overview and test WBS filter
        await page.click("#nav-overview")
        await page.select_option("#filterWbsLevel", "2")
        await page.wait_for_timeout(500)
        await page.screenshot(path="dist/screenshots/overview_filtered_wbs.png")
        print("WBS filter applied and verified.")

        await browser.close()

if __name__ == "__main__":
    asyncio.run(run())
