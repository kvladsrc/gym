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

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  ssize_t n = 0;
  std::cin >> n;
  vector<int64_t> a(n);
  for (auto& i : a) {
    std::cin >> i;
  }

  map<int64_t, ssize_t> m;
  m[0] = 0;
  ssize_t last = 0;
  int64_t res = 0;
  int64_t sum = 0;
  for (ssize_t idx = 0; idx < n; ++idx) {
    sum += a[idx];
    if (m.count(sum) != 0) {
      last = max(last, m[sum] + 1);
    }
    m[sum] = idx + 1;
    res += idx + 1 - last;
  }

  std::cout << res << "\n";

  return 0;
}
