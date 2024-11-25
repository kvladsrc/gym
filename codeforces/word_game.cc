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

    unordered_map<string, bool> first;
    for (int i = 0; i < n; ++i) {
      string buf;
      std::cin >> buf;
      first[buf] = true;
    }

    unordered_map<string, bool> second;
    for (int i = 0; i < n; ++i) {
      string buf;
      std::cin >> buf;
      second[buf] = true;
    }

    unordered_map<string, bool> third;
    for (int i = 0; i < n; ++i) {
      string buf;
      std::cin >> buf;
      third[buf] = true;
    }

    int f_score = 0, s_score = 0, t_score = 0;
    for (auto s : first) {
      if (!s.second) continue;
      if (second[s.first]) {
        if (third[s.first]) {
          continue;
        }

        f_score++;
        continue;
      }

      if (third[s.first]) {
        f_score++;
        continue;
      }

      f_score += 3;
    }

    for (auto s : second) {
      if (!s.second) continue;
      if (first[s.first]) {
        if (third[s.first]) {
          continue;
        }

        s_score++;
        continue;
      }

      if (third[s.first]) {
        s_score++;
        continue;
      }

      s_score += 3;
    }

    for (auto s : third) {
      if (!s.second) continue;
      if (first[s.first]) {
        if (second[s.first]) {
          continue;
        }

        t_score++;
        continue;
      }

      if (second[s.first]) {
        t_score++;
        continue;
      }

      t_score += 3;
    }

    std::cout << f_score << " ";
    std::cout << s_score << " ";
    std::cout << t_score << "\n";
  }

  return 0;
}
