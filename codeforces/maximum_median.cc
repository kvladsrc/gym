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
  int64_t n, k;
  std::cin >> n >> k;

  vector<int64_t> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  sort(a.begin(), a.end());

  int mid = n / 2;
  int64_t res = a[n / 2];
  for (int64_t i = 1; mid + i < n; ++i) {
    int64_t diff = a[mid + i] - res;

    if ((diff * i) > k) {
      res += k / i;
      // Preventing post loop branch. So k > 0 only if we reach the
      // end of array.
      k = 0;
      break;
    }

    res += diff;
    k -= diff * i;
  }

  if (k > 0) {
    res += k / ((n / 2) + 1);
  }

  std::cout << res << "\n";

  return 0;
}
