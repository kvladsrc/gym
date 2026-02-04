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
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    vector<int> a(n);
    int64_t odd_sum = 0;
    int64_t even_sum = 0;

    for (int i = 0; i < n; ++i) {
      std::cin >> a[i];
      if (i % 2) {
        odd_sum += a[i];
      } else {
        even_sum += a[i];
      }
    }

    for (int i = 0; i < n; ++i) {
      if ((i % 2 && odd_sum > even_sum) ||
          (i % 2 == 0 && odd_sum <= even_sum)) {
        std::cout << a[i] << " ";
      } else {
        std::cout << 1 << " ";
      }
    }
    std::cout << "\n";
  }

  return 0;
}
