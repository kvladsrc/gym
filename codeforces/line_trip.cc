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

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    size_t n = 0;
    int x = 0;
    std::cin >> n >> x;

    vector<int> a(n + 1);
    a.front() = 0;
    for (size_t idx = 1; idx <= n; ++idx) {
      std::cin >> a[idx];
    }

    int longest = (x - a.back()) * 2;
    for (size_t idx = 1; idx <= n; ++idx) {
      longest = max(longest, a[idx] - a[idx - 1]);
    }

    std::cout << longest << "\n";
  }

  return 0;
}
