#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

uint64_t attack(const vector<int>& a, uint64_t k) {
  uint64_t res = 0;
  for (size_t i = 1; i < a.size(); ++i) {
    auto casted_diff = static_cast<uint64_t>(a[i] - a[i - 1]);
    if (casted_diff < k) {
      res += casted_diff;
    } else {
      res += k;
    }
  }
  res += k;  // Last attack.
  return res;
}

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    uint64_t h;
    std::cin >> h;

    vector<int> a(n);
    for (auto& i : a) {
      std::cin >> i;
    }
    sort(a.begin(), a.end());

    uint64_t l = 1, r = h;
    uint64_t res = -1;
    while (l <= r) {
      uint64_t mid = (l + r) / 2;
      if (attack(a, mid) >= h) {
        res = mid;
        r = mid - 1;
      } else {
        l = mid + 1;
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}
