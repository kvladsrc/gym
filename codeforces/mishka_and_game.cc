#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  int m_wins = 0, c_wins = 0;

  for (int i = 0; i < n; ++i) {
    int m, c;
    std::cin >> m >> c;

    if (m > c) {
      m_wins++;
      continue;
    }

    if (m < c) {
      c_wins++;
      continue;
    }
  }

  if (m_wins == c_wins) {
    std::cout << "Friendship is magic!^^"
              << "\n";
  } else {
    if (m_wins > c_wins) {
      std::cout << "Mishka"
                << "\n";
    } else {
      std::cout << "Chris"
                << "\n";
    }
  }

  return 0;
}
