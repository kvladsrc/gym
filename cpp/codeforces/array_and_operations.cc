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

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n, k;
    int64_t sum = 0;
    std::cin >> n >> k;

    vector<int> a(n);
    for (auto &i : a) {
      std::cin >> i;
      sum += i;
    }

    sort(a.begin(), a.end());

    vector<int> mn(k);
    vector<int> mx(k);
    size_t r = n - 1;
    for (int i = 0; i < k; ++i) {
      mx[i] = a[r--];
      sum -= mx[i];
    }
    for (int i = 0; i < k; ++i) {
      mn[i] = a[r--];
      sum -= mn[i];
    }
    for (int i = 0; i < k; ++i) {
      sum += mn[i] / mx[i];
    }

    std::cout << sum << "\n";
  }

  return 0;
}
