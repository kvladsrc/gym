#include <algorithm>
#include <climits>
#include <cmath>
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

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    vector<int64_t> pref_sums(n + 1);
    pref_sums[0] = 0;
    for (int i = 1; i <= n; ++i) {
      int64_t buf;
      std::cin >> buf;
      pref_sums[i] = pref_sums[i - 1] + buf;
    }

    auto all = pref_sums.back();
    sort(pref_sums.begin(), pref_sums.end());

    bool res = true;
    if (pref_sums[1] <= 0) {
      res = false;
    } else if (pref_sums[n - 1] >= all) {
      res = false;
    }

    std::cout << (res ? "YES" : "NO") << "\n";
  }

  return 0;
}
