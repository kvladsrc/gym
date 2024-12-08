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

int n_in_pal(string &s, int n) {
  int res = 0;
  int size = s.size();
  if (s[n - 1] != '*' && s[n] != '*' && n - 1 >= 0 && s[n] == s[n - 1]) {
    res++;
  }
  if (s[n - 2] != '*' && s[n] != '*' && n - 2 >= 0 && s[n] == s[n - 2]) {
    res++;
  }
  if (s[n + 1] != '*' && s[n] != '*' && n + 1 < size && s[n] == s[n + 1]) {
    res++;
  }
  if (s[n + 2] != '*' && s[n] != '*' && n + 2 < size && s[n] == s[n + 2]) {
    res++;
  }
  return res;
}

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while (t--) {
    string s;
    std::cin >> s;
    auto s_cpy = s;
    int size = s.size();

    // Lemma 1: any palindrome contain a 2 or 3 letter palindrome at
    // the center.
    //
    // Lemma 2: to remove any palindrome with len > 1 we need to
    // remove any palindome with len 2 or 3.
    //
    // After some attempts come with the solution: greedy to mark all
    // letters inside any 2 or 3 palindrom, then greedy restore some
    // chars if it possible.

    int res = 0;
    for (int i = 0; i < size; ++i) {
      if (n_in_pal(s, i) > 0) {
        res++;
        s_cpy[i] = '*';
      }
    }

    for (int i = 0; i < size; ++i) {
      if (s_cpy[i] != '*') {
        continue;
      }

      // Check;
      s_cpy[i] = s[i];
      if (n_in_pal(s_cpy, i) == 0) {
        res--;
      } else {
        s_cpy[i] = '*';
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}
