// SPDX-License-Identifier: AGPL-3.0-only

import Darwin
import XCTest
@testable import GameTranslatorCore

final class LocalNetworkTests: XCTestCase {
    private func address(_ text: String) -> in_addr { in_addr(s_addr: inet_addr(text)) }

    func testScanCoversTheWholeHomeNetworkExceptThisComputer() {
        let targets = LocalNetwork.scanTargets([address("192.168.1.40")])
        XCTAssertEqual(targets.count, 253)
        XCTAssertTrue(targets.contains("192.168.1.1"))
        XCTAssertTrue(targets.contains("192.168.1.254"))
        XCTAssertFalse(targets.contains("192.168.1.40"))
    }

    func testTwoAdaptersOnTheSameNetworkAreScannedOnce() {
        let targets = LocalNetwork.scanTargets([address("10.0.0.5"), address("10.0.0.6")])
        XCTAssertEqual(targets.count, Set(targets).count)
    }

    func testOnlyHomeNetworkAddressesArePrivate() {
        XCTAssertTrue(LocalNetwork.isPrivate(address("192.168.0.10")))
        XCTAssertTrue(LocalNetwork.isPrivate(address("172.20.1.1")))
        XCTAssertTrue(LocalNetwork.isPrivate(address("10.1.2.3")))
        XCTAssertFalse(LocalNetwork.isPrivate(address("8.8.8.8")))
        XCTAssertFalse(LocalNetwork.isPrivate(address("172.32.0.1")))
    }
}
