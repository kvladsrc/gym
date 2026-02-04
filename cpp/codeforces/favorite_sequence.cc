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

vector<int> fav(const vector<int>& seq) {
  int left = 0, right = seq.size() - 1;
  bool left_turn = true;
  vector<int> res(seq.size());
  for (size_t i = 0; i < seq.size(); ++i) {
    if (left_turn) {
      res[i] = seq[left++];
    } else {
      res[i] = seq[right--];
    }
    left_turn = !left_turn;
  }
  return res;
}

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    vector<int> seq(n);
    for (int i = 0; i < n; ++i) {
      std::cin >> seq[i];
    }
    for (int i : fav(seq)) {
      std::cout << i << " ";
    }
    std::cout << "\n";
  }
  return 0;
}
