#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
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
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

struct card {
  char suit;
  char rank;

  bool fit(const card& other) {
    return other.suit == suit || other.rank == rank;
  }
};

int main(int argc, char* argv[]) {
  card on_desk;
  std::cin >> on_desk.rank >> on_desk.suit;
  bool res = false;

  for (int i = 0; i < 5; ++i) {
    card buf;
    std::cin >> buf.rank >> buf.suit;
    if (on_desk.fit(buf)) {
      res = true;
    }
  }

  if (res) {
    std::cout << "YES"
              << "\n";
  } else {
    std::cout << "NO"
              << "\n";
  }

  return 0;
}
