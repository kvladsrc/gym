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

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    vector<int> pref_sum(n + 1);
    pref_sum.front() = 0;
    int64_t res = 0;
    for (int i = 1; i <= n; ++i) {
      int buf;
      std::cin >> buf;
      pref_sum[i] = pref_sum[i - 1];
      if (buf < i) {
        pref_sum[i]++;
        if (buf > 1) {
          res += pref_sum[buf - 1];
        }
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}
