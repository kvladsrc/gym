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
    int n, x;
    std::cin >> n >> x;

    int res = 0;
    vector<int> a;
    for (int i = 0; i < n; ++i) {
      int buf;
      std::cin >> buf;
      if (buf >= x) {
        res++;
      } else {
        a.push_back(buf);
      }
    }

    sort(a.begin(), a.end());

    int ahead = 1;
    for (int i = a.size() - 1; i >= 0; --i) {
      if ((a[i] * ahead) >= x) {
        res++;
        ahead = 1;
      } else {
        ahead++;
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}
