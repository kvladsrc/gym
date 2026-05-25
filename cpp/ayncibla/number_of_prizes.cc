#include <iostream>
#include <vector>

using std::vector;

static vector<int> optimal_summands(int n) {
  vector<int> summands;
  int i = 1;
  while (n != 0) {
    if ((n - i) == 0) {
      summands.push_back(i);
      break;
    }

    if ((n - i) > i) {
      summands.push_back(i);
      n -= i;
    }

    i++;
  }
  return summands;
}

int main() {
  int n = 0;
  std::cin >> n;
  vector<int> const summands = optimal_summands(n);
  std::cout << summands.size() << '\n';
  for (int const summand : summands) {
    std::cout << summand << ' ';
  }
}
