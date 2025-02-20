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
    int q = 0;
    std::cin >> n >> q;

    vector<int> a(n);
    for (size_t idx = n; idx > 0; --idx) {
      std::cin >> a[idx - 1];
    }

    /*

      This is an interesting problem. Initially tried solving it using
      dynamic programming, but since the constraints on Q are too
      large, switched to a greedy approach (processing in reverse
      order).

      - We need at least 1 IQ to select the first element, so we set c1 = 1;
      - To select i elements, the minimum required IQ is determined by:
      c[i] = max(c[i-1], min(a[i], c[i-1] + 1)).
      - Once we reach an index j where c[j] equals Q, we can choose all
      subsequent elements a[k] (for k > j) that satisfy a[k] ≤ Q.

      This solution is optimal because:
      - If we select an element a[k] (with k > j) where a[k] > Q, then even in
      the best-case scenario, the IQ at the first position would drop to 0,
      forcing us to skip that element. In the worst-case scenario, we might have
      to skip multiple elements.

    */

    vector<bool> res(n, false);
    res.front() = true;
    int c = 1;
    for (size_t idx = 1; idx < n; ++idx) {
      c = max(c, min(c + 1, a[idx]));

      if (c <= q) {
        res[idx] = true;
      } else {
        c = q;
      }
    }

    for (size_t idx = n; idx > 0; --idx) {
      if (res[idx - 1]) {
        std::cout << 1;
      } else {
        std::cout << 0;
      }
    }
    std::cout << "\n";
  }

  return 0;
}
