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
    vector<int> monsters(n);
    int one_hp_cnt = 0;
    for (auto& i : monsters) {
      std::cin >> i;
      if (i == 1) one_hp_cnt++;
    }
    int res = one_hp_cnt / 2;
    res += n - one_hp_cnt + (one_hp_cnt % 2);
    std::cout << res << "\n";
  }

  return 0;
}
