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
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::map;
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

int64_t bs(const vector<int64_t>& a, int64_t el) {
  int64_t res = a.size();
  int64_t l = 0, r = a.size() - 1;
  while (l <= r) {
    int64_t mid = (l + r) / 2;
    if (a[mid] >= el) {
      res = mid;
      r = mid - 1;
    } else {
      l = mid + 1;
    }
  }
  return res;
}

int main(int argc, char* argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int64_t n, d;
  std::cin >> n >> d;
  vector<int64_t> a(n);
  for (auto& i : a) {
    std::cin >> i;
  }

  // For every point x let's calculate the number of ways to pick
  // three points in way there x will be top left point by binary
  // search.

  int64_t res = 0;
  for (size_t idx = 0; idx < a.size(); ++idx) {
    auto p = a[idx];
    auto fp = bs(a, p + d + 1);
    fp -= idx + 1;
    res += (fp * (fp - 1)) / 2;
  }
  std::cout << res << "\n";

  return 0;
}
