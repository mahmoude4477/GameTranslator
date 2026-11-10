// SPDX-License-Identifier: AGPL-3.0-only

import Darwin
import Foundation

/// Yerel ağdaki LG webOS TV'leri SSDP ile bulur.
public enum LGDiscovery {
    static let searchTarget = "urn:lge-com:service:webos-second-screen:1"

    public static func search(timeout: Duration = .seconds(3)) async -> [LGTV] {
        let hosts = await Task.detached { collectHosts(searchTarget: searchTarget, timeout: timeout) }.value
        var tvs: [LGTV] = []
        for host in Set(hosts).sorted() {
            tvs.append(LGTV(name: await friendlyName(host: host) ?? "LG TV", host: host))
        }
        return tvs
    }

    /// SSDP yanıtından TV'nin adresini çıkarır; LG'nin ikinci ekran servisi değilse nil.
    static func host(fromSSDPResponse response: String, searchTarget: String = searchTarget) -> String? {
        var location: String?
        var isLG = false
        for line in response.components(separatedBy: "\r\n") {
            let parts = line.split(separator: ":", maxSplits: 1).map { $0.trimmingCharacters(in: .whitespaces) }
            guard parts.count == 2 else { continue }
            switch parts[0].uppercased() {
            case "LOCATION": location = parts[1]
            case "ST", "NT": isLG = isLG || parts[1] == searchTarget
            default: break
            }
        }
        guard isLG, let location, let url = URL(string: location) else { return nil }
        return url.host
    }

    static func collectHosts(searchTarget: String, timeout: Duration) -> [String] {
        let fd = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP)
        guard fd >= 0 else { return [] }
        defer { close(fd) }

        var tv = timeval(tv_sec: 0, tv_usec: 300_000)
        setsockopt(fd, SOL_SOCKET, SO_RCVTIMEO, &tv, socklen_t(MemoryLayout<timeval>.size))

        var address = sockaddr_in()
        address.sin_family = sa_family_t(AF_INET)
        address.sin_port = in_port_t(1900).bigEndian
        address.sin_addr.s_addr = inet_addr("239.255.255.250")

        let request = "M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\nMAN: \"ssdp:discover\"\r\nMX: 2\r\nST: \(searchTarget)\r\n\r\n"
        func send() {
            _ = request.withCString { pointer in
                withUnsafePointer(to: &address) {
                    $0.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                        sendto(fd, pointer, strlen(pointer), 0, $0, socklen_t(MemoryLayout<sockaddr_in>.size))
                    }
                }
            }
        }
        // İstek her ağ bağdaştırıcısından ayrı gönderilir: varsayılan bağdaştırıcı VPN olabilir ve TV o ağda değildir.
        let interfaces = LocalNetwork.privateAddresses()
        for _ in 0..<2 {
            if interfaces.isEmpty { send() }
            for var local in interfaces {
                setsockopt(fd, IPPROTO_IP, IP_MULTICAST_IF, &local, socklen_t(MemoryLayout<in_addr>.size))
                send()
            }
        }

        var hosts: [String] = []
        let end = ContinuousClock.now + timeout
        var buffer = [UInt8](repeating: 0, count: 2048)
        while ContinuousClock.now < end {
            let count = recv(fd, &buffer, buffer.count, 0)
            guard count > 0 else { continue }
            if let host = host(fromSSDPResponse: String(decoding: buffer[0..<count], as: UTF8.self), searchTarget: searchTarget) { hosts.append(host) }
        }
        return hosts
    }

    /// TV'nin ağdaki adını (ör. "[LG] webOS TV UK7550") cihaz tanımından okur.
    private static func friendlyName(host: String) async -> String? {
        // webOS cihaz tanımı 1990 numaralı portta yayınlanır; okunamazsa genel ad kullanılır.
        guard let url = URL(string: "http://\(host):1990/"),
              let (data, _) = try? await URLSession.shared.data(from: url) else { return nil }
        let xml = String(decoding: data, as: UTF8.self)
        guard let start = xml.range(of: "<friendlyName>"), let end = xml.range(of: "</friendlyName>", range: start.upperBound..<xml.endIndex) else { return nil }
        return String(xml[start.upperBound..<end.lowerBound])
    }
}
