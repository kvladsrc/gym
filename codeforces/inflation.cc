#include <algorithm>
#include <climits>
#include <cmath>
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

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n, k;
    std::cin >> n >> k;

    vector<int64_t> pref_sum(n + 1);
    pref_sum.front() = 0.0;
    for (int i = 1; i <= n; ++i) {
      int64_t buf;
      std::cin >> buf;
      pref_sum[i] = pref_sum[i - 1] + buf;
    }

    // Increasing the initial price reduce inflation coefs on each
    // month. So, we can only increase initial price to fit every
    // month reqirinment.
    int64_t res = 0;
    for (int i = 2; i <= n; ++i) {
      auto cur_month = pref_sum[i] - pref_sum[i - 1];
      auto delta = (cur_month * 100) - ((pref_sum[i - 1] + res) * k);

      if (delta > 0) {
        res += delta / k;
        if ((delta % k) != 0) {
          res++;
        }
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}
