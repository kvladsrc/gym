#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <math.h>
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

    string s;
    std::cin >> s;

    // Looking for the median position. Then rearanging sheeps around
    // it. Proof concept: Moving line center to left or right adds
    // sheep_cnt / 2 + 1 (mid sheep) moves on opposite side and reduce
    // by sheep_cnt / 2 in best case number of moves in side direction
    // to move:
    // ***...*.* - 7 moves
    //   ^
    // ***...*.* - 8 moves
    //    ^
    // Even number of sheeps case.
    // **....*..* - 10 moves
    //  ^
    // **....*..* - 10 moves
    //       ^
    // Looks like it same. Have no proof, but let's try.
    vector<int> positions;
    for (int i = 0; i < n; ++i) {
      if (s[i] == '.')
        continue;

      positions.push_back(i + 1);
    }

    if (positions.size() == 0) {
      std::cout << 0 << "\n";
      continue;
    }

    int median = positions[positions.size() / 2];
    int64_t first_sheep_pos = median - (positions.size() / 2);
    int64_t cur_sheep_to_place = 0;
    int64_t res = 0;
    for (auto i : positions) {
      auto need_pos = first_sheep_pos + cur_sheep_to_place++;
      res += abs(need_pos - i);
    }

    std::cout << res << "\n";
  }

  return 0;
}
