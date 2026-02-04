#include <algorithm>
#include <cstdlib>
#include <iostream>
#include <set>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char* argv[]) {
  int n;
  std::cin >> n;
  vector<int> cards(n);
  for (int i = 0; i < n; ++i) {
    std::cin >> cards[i];
  }
  int start = 0, end = n - 1;
  int s_scores = 0, d_scores = 0;
  bool s_turn = true;
  while (start <= end) {
    if (s_turn) {
      if (cards[start] > cards[end]) {
        s_scores += cards[start];
        start++;
      } else {
        s_scores += cards[end];
        end--;
      }
    } else {
      if (cards[start] > cards[end]) {
        d_scores += cards[start];
        start++;
      } else {
        d_scores += cards[end];
        end--;
      }
    }

    s_turn = !s_turn;
  }

  std::cout << s_scores << " " << d_scores << "\n";

  return 0;
}
