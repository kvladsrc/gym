#include <algorithm>
#include <climits>
#include <cmath>
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

    std::map<int, int> cnt;
    int most_freq = 0;
    for (int i = 0; i < n; ++i) {
      int buf;
      std::cin >> buf;
      cnt[buf]++;
      most_freq = max(most_freq, cnt[buf]);
    }

    int res = 0;
    if (n % 2 != 0) {
      most_freq--;
      res++;
    }

    res += max(most_freq - (n / 2), 0) * 2;
    std::cout << res << "\n";
  }

  return 0;
}
