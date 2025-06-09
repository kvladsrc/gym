#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <optional>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <tuple>
#include <unordered_map>
#include <utility>
#include <vector>

using std::abs;
using std::endl;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

using res = vector<std::tuple<bool, bool, bool>>;

std::optional<res> solve(uint64_t u, uint64_t v, uint64_t bit) {
  if (u == 0 && v == 0) {
    if (bit != 0) {
      return std::nullopt;
    }
    res r;
    r.push_back(std::make_tuple(false, false, false));
    return r;
  }

  auto bu = u % 2;
  auto bv = v % 2;
  auto nu = u / 2;
  auto nv = v / 2;

  for (uint64_t c = 0; c <= 1; ++c) {
    for (uint64_t b = 0; b <= 1; ++b) {
      for (uint64_t a = 0; a <= 1; ++a) {
        if (((a ^ b) ^ c) == bu && (a + b + c + bit) % 2 == bv) {
          auto nbit = (a + b + c + bit) / 2;
          auto n = solve(nu, nv, nbit);
          if (n.has_value()) {
            n.value().push_back({a == 1, b == 1, c == 1});
            return n;
          }
        }
      }
    }
  }

  return std::nullopt;
}

int main(int /*argc*/, char* /*argv*/[]) {
  uint64_t u = 0;
  uint64_t v = 0;
  std::cin >> u >> v;

  if (u % 2 != v % 2) {
    std::cout << -1 << "\n";
    return 0;
  }

  if (u == 0 && v == 0) {
    std::cout << 0 << "\n";
    return 0;
  }

  auto up = (u ^ v) & u;
  auto down = (u ^ v) & v;

  vector<pair<bool, bool>> table;
  while (up != 0 || down != 0) {
    table.push_back({up % 2 == 1, down % 2 == 1});
    up /= 2;
    down /= 2;
  }

  if (table.empty()) {
    std::cout << 1 << "\n";
    std::cout << u << "\n";
    return 0;
  }

  bool down_last = false;
  for (auto p : table) {
    if (p.first) {
      down_last = false;
    }
    if (p.second) {
      down_last = true;
    }
  }

  if (!down_last) {
    std::cout << -1 << "\n";
    return 0;
  }

  auto r = solve(u, v, 0);
  if (r.has_value()) {
    int64_t a = 0;
    int64_t b = 0;
    int64_t c = 0;
    for (auto t : r.value()) {
      a *= 2;
      b *= 2;
      c *= 2;
      a += std::get<0>(t) ? 1 : 0;
      b += std::get<1>(t) ? 1 : 0;
      c += std::get<2>(t) ? 1 : 0;
    }

    vector<int64_t> res_vector;
    if (a != 0) {
      res_vector.push_back(a);
    }

    if (b != 0) {
      res_vector.push_back(b);
    }

    if (c != 0) {
      res_vector.push_back(c);
    }

    std::cout << res_vector.size() << "\n";
    for (auto i : res_vector) {
      std::cout << i << " ";
    }
    std::cout << "\n";
  } else {
    std::cout << -1 << "\n";
  }

  return 0;
}
