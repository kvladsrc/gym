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

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    size_t n = 0;
    std::cin >> n;

    vector<int64_t> a(n);
    for (auto &i : a) {
      std::cin >> i;
    }

    map<int64_t, bool> used;
    vector<int64_t> psum(n + 1);
    psum.front() = 0;

    int res = 0;
    for (size_t idx = 1; idx <= n; ++idx) {
      psum[idx] = psum[idx - 1];
      psum[idx] += a[idx - 1];
      used[a[idx - 1]] = true;

      if (psum[idx] % 2 != 0) {
        continue;
      }

      if (used[psum[idx] / 2]) {
        res++;
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}
