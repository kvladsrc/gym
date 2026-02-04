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

int main(int argc, char* argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  string s;
  std::cin >> s;

  // .......xxx
  // xx.......x
  //
  // xxxxx.....
  // .....xxxxx
  //
  // We can remove some chars from the end of mid substring until the
  // size is equal to suffix size. Since they both are equal to prefix
  // then they are all equal.
  //
  // .....xxxxx
  // xx.....xxx
  // xxxxx.....
  //
  // Looking for a second largest or equal len which is also a suffix.

  vector<int> pref_len(s.size());
  pref_len.front() = 0;

  int mx_suff = 0;
  int scnd_suff = 0;
  int mx_mid = 0;
  for (size_t idx = 1; idx < s.size(); ++idx) {
    pref_len[idx] = 0;
    for (size_t idx2 = 0; idx + idx2 < s.size(); ++idx2) {
      if (s[idx2] == s[idx + idx2]) {
        pref_len[idx]++;
      } else {
        break;
      }
    }

    if (idx == 1 && pref_len[idx] + 1 == static_cast<int>(s.size())) {
      break;
    }

    if (pref_len[idx] + idx == s.size()) {
      if (mx_suff < pref_len[idx]) {
        scnd_suff = mx_suff;
        mx_suff = pref_len[idx];
        continue;
      }

      scnd_suff = max(scnd_suff, pref_len[idx]);
    }

    mx_mid = max(mx_mid, pref_len[idx]);
  }

  // Need to proof edge case when pref_len[1] = s.size() - 1.
  // .........x
  // x.........
  //
  // If two strings are equal removing one char from the end or
  // begining of both give the same strings. So:
  //
  // x........x = xx........
  // ........xx = x........x
  //
  // Then:
  //
  // ........xx = xx........ = x........x
  if (pref_len[1] == static_cast<int>(s.size()) - 1) {
    s.pop_back();
    s.pop_back();

    if (s.size() > 0) {
      std::cout << s << "\n";
    } else {
      std::cout << "Just a legend" << "\n";
    }

    return 0;
  }

  int res;
  if (mx_suff > mx_mid) {
    res = scnd_suff;
  } else {
    res = mx_suff;
  }

  if (res) {
    for (int i = 0; i < res; ++i) {
      std::cout << s[i];
    }
    std::cout << "\n";
  } else {
    std::cout << "Just a legend" << "\n";
  }

  return 0;
}
