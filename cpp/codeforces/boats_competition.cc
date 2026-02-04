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

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    vector<int> a(2 * n + 1, 0);
    for (int i = 0; i < n; ++i) {
      int buf;
      std::cin >> buf;
      a[buf]++;
    }

    int res = 0;
    for (int s = 2; s <= (2 * n); ++s) {
      int cnt = 0;
      for (int w = 1; w < (s + 1) / 2; ++w) {
        cnt += min(a[w], a[s - w]);
      }
      if (s % 2 == 0) {
        cnt += a[s / 2] / 2;
      }
      res = max(res, cnt);
    }

    std::cout << res << "\n";
  }

  return 0;
}
