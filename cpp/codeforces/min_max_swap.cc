#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
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

    vector<int> a(n);
    for (auto &i : a) std::cin >> i;

    vector<int> b(n);
    for (auto &i : b) std::cin >> i;

    int max_b = 0, max_a = 0;
    for (int i = 0; i < n; ++i) {
      int min_buf, max_buf;
      min_buf = min(a[i], b[i]);
      max_buf = max(a[i], b[i]);

      max_b = max(max_b, max_buf);
      max_a = max(max_a, min_buf);
    }

    std::cout << max_b * max_a << "\n";
  }

  return 0;
}
