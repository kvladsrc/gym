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
    vector<double> a(n);
    int64_t sum = 0;
    for (auto& i : a) {
      std::cin >> i;
      sum += i;
    }

    double av = static_cast<double>(sum) / static_cast<double>(n);

    map<double, int64_t> val_cnt;
    int64_t res = 0;
    for (int i = 0; i < n; ++i) {
      auto cur = a[i];

      // Looking for an element x where (sum - cur - x) / (n - 2) =
      // av.
      // sum - cur - x = av * (n - 2).
      // sum - cur = (av * (n - 2)) + x
      // sum - cur - (av * (n - 2)) = x
      auto need = sum - cur - (av * (n - 2));
      res += val_cnt[need];
      val_cnt[cur]++;
    }

    std::cout << res << "\n";
  }

  return 0;
}
