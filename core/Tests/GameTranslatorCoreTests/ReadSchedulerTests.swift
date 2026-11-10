// SPDX-License-Identifier: AGPL-3.0-only

import XCTest
@testable import GameTranslatorCore

final class ReadSchedulerTests: XCTestCase {
    private let still = [UInt8](repeating: 40, count: 16)

    func testFirstFrameIsAlwaysRead() {
        var scheduler = ReadScheduler()
        XCTAssertEqual(scheduler.plan(signature: still), .read(deep: true))
    }

    func testUnchangedRegionReusesTheLastReading() {
        var scheduler = ReadScheduler()
        _ = scheduler.plan(signature: still)
        scheduler.record("Hello there")
        XCTAssertEqual(scheduler.plan(signature: still), .reuse("Hello there"))
    }

    func testVideoNoiseCountsAsUnchanged() {
        var scheduler = ReadScheduler()
        _ = scheduler.plan(signature: still)
        scheduler.record("")
        var noisy = still
        noisy[3] += 8
        XCTAssertEqual(scheduler.plan(signature: noisy), .reuse(""))
    }

    func testOneChangedSampleTriggersAReading() {
        // Daktilo efektinde yalnız bir harf eklenir; ortalama değil en büyük fark bakılır.
        var scheduler = ReadScheduler()
        _ = scheduler.plan(signature: still)
        scheduler.record("Hel")
        var typed = still
        typed[3] += 40
        XCTAssertEqual(scheduler.plan(signature: typed), .read(deep: false))
    }

    func testReusedReadingIsRefreshedAfterALimit() {
        var scheduler = ReadScheduler(maxReuse: 2, deepEvery: 2)
        _ = scheduler.plan(signature: still)
        scheduler.record("a line")
        XCTAssertEqual(scheduler.plan(signature: still), .reuse("a line"))
        XCTAssertEqual(scheduler.plan(signature: still), .reuse("a line"))
        XCTAssertEqual(scheduler.plan(signature: still), .read(deep: false))
    }

    func testSlowerSearchRunsOnlyOnEveryThirdEmptyFrame() {
        var scheduler = ReadScheduler(deepEvery: 3)
        var plans: [ReadScheduler.Plan] = []
        for value in 0..<6 {
            plans.append(scheduler.plan(signature: [UInt8](repeating: UInt8(value * 50), count: 16)))
            scheduler.record("")
        }
        XCTAssertEqual(plans, [.read(deep: true), .read(deep: false), .read(deep: false),
                               .read(deep: true), .read(deep: false), .read(deep: false)])
    }

    func testTextFoundResetsTheSlowerSearchCount() {
        var scheduler = ReadScheduler(deepEvery: 2)
        _ = scheduler.plan(signature: [1, 1])
        scheduler.record("line")
        XCTAssertEqual(scheduler.plan(signature: [90, 90]), .read(deep: false))
        scheduler.record("")
        XCTAssertEqual(scheduler.plan(signature: [180, 180]), .read(deep: true))
    }

    func testResetForgetsThePreviousFrame() {
        var scheduler = ReadScheduler()
        _ = scheduler.plan(signature: still)
        scheduler.record("old")
        scheduler.reset()
        XCTAssertEqual(scheduler.plan(signature: still), .read(deep: true))
    }

    func testNewSizeIsAlwaysAChange() {
        var scheduler = ReadScheduler()
        _ = scheduler.plan(signature: still)
        scheduler.record("x")
        XCTAssertEqual(scheduler.plan(signature: [40, 40]), .read(deep: false))
    }

    func testAlwaysReadDisablesReuse() {
        var scheduler = ReadScheduler.always
        _ = scheduler.plan(signature: still)
        scheduler.record("")
        XCTAssertEqual(scheduler.plan(signature: still), .read(deep: true))
    }
}
