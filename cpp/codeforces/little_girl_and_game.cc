#include <algorithm>
#include <cstdint>
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
  string s;
  std::cin >> s;
  vector<int> cnt(26);
  int odd_cnt = 0;
  for (char c : s) {
    cnt[c - 'a']++;
    if (cnt[c - 'a'] % 2) {
      odd_cnt++;
    } else {
      odd_cnt--;
    }
  }

  bool first_turn = true;

  // Better solution: if odd_cnt even second player always wins.
  while (true) {
    if (odd_cnt <= 1) {
      std::cout << (first_turn ? "First" : "Second") << "\n";
      break;
    }

    bool removed = false;

    for (int c = 0; c < 26; ++c) {
      if (cnt[c] > 0 && (cnt[c] % 2 == 0)) {
        cnt[c]--;
        odd_cnt++;
        removed = true;
        break;
      }
    }

    if (!removed) {
      for (int c = 0; c < 26; ++c) {
        if (cnt[c] > 0) {
          cnt[c]--;
          odd_cnt--;
          break;
        }
      }
    }

    first_turn = !first_turn;
  }

  return 0;
}
